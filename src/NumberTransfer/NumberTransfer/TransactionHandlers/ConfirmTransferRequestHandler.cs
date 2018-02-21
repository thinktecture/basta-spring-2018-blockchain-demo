﻿using System.Threading.Tasks;
using Grpc.Core;
using NumberTransfer.Repositories;
using NumberTransfer.Services;
using NumberTransfer.Transactions;
using Types;

namespace NumberTransfer.TransactionHandlers
{
    // This class is basically the same as RequestTransferHandler
    public class ConfirmTransferRequestHandler : ITransactionHandler, ITransactionHandler<ConfirmTransferRequest>
    {
        private readonly TransactionTokenValidationService _transactionTokenValidationService;
        private readonly ICallNumberRepository _callNumberRepository;

        public ConfirmTransferRequestHandler(TransactionTokenValidationService transactionTokenValidationService,
            ICallNumberRepository callNumberRepository)
        {
            _transactionTokenValidationService = transactionTokenValidationService;
            _callNumberRepository = callNumberRepository;
        }

        public async Task<ResponseCheckTx> CheckTx(TransactionToken transactionToken, object data, RequestCheckTx request, ServerCallContext context)
        {
            if (!(data is ConfirmTransferRequest payload))
            {
                return ResponseHelper.Check.NoPayload();
            }

            var callNumber = await _callNumberRepository.Get(payload.PhoneNumber);

            if (callNumber == null)
            {
                return ResponseHelper.Check.Create(CodeType.UnknownCallNumber, "Unknown call number.");
            }

            if (!IsVerifiedCaller(transactionToken, callNumber.Owner))
            {
                return ResponseHelper.Check.Unauthorized();
            }

            if (callNumber.TransferRequestedTo != payload.NewOwner)
            {
                return ResponseHelper.Check.Create(CodeType.UnknownNewOwner, "Unknown new owner.");
            }

            if (!callNumber.TransferRequestStarted.HasValue)
            {
                return ResponseHelper.Check.Create(CodeType.NoTransferInitiated, "Transfer was not initiated.");
            }

            return ResponseHelper.Check.Ok();
        }

        public async Task<ResponseDeliverTx> DeliverTx(TransactionToken transactionToken, object data, RequestDeliverTx request, ServerCallContext context)
        {
            if (!(data is ConfirmTransferRequest payload))
            {
                return ResponseHelper.Deliver.NoPayload();
            }

            var callNumber = await _callNumberRepository.Get(payload.PhoneNumber);

            if (callNumber == null)
            {
                return ResponseHelper.Deliver.Create(CodeType.UnknownCallNumber, "Unknown call number.");
            }

            if (!IsVerifiedCaller(transactionToken, callNumber.Owner))
            {
                return ResponseHelper.Deliver.Unauthorized();
            }

            callNumber.Owner = callNumber.TransferRequestedTo;
            callNumber.TransferRequestedTo = string.Empty;
            callNumber.TransferRequestStarted = null;

            await _callNumberRepository.Update(callNumber);

            return ResponseHelper.Deliver.Ok();
        }
        
        private bool IsVerifiedCaller(TransactionToken token, string owner)
        {
            _transactionTokenValidationService.Validate(token, owner);

            return token.IsValid;
        }
    }
}
